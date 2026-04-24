use super::{user_error::UserError, email::Email};

#[derive(Debug, Clone)]
pub struct User {
    pub id: u64,
    pub email: Email,
    pub password_hash: String,
}

/*
impl User {
    pub fn new(id: u64, email: String, password_hash: String) -> Self {
        Self { id, email, password_hash, }
    }
}
*/

impl User {
    pub fn create(id: u64, email: String, password_hash: String) -> Result<User, UserError> {
        let emilio = Email::parse(email)?;
        // if !email.contains("@") { return Err(UserError::InvalidEmail); }
        if password_hash.is_empty() { return Err(UserError::EmptyPassword); }

        Ok(Self { id: id, email: emilio, password_hash, })
    } 
}
