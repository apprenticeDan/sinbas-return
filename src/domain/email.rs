//use super::user_error::UserError;

#[derive(Debug)]
pub enum EmailError { Empty, InvalidFormat, }

#[derive(Debug, Clone)]
pub struct Email(String);

impl Email {
    pub fn parse(value: String) -> Result<Self, EmailError> {
        if value.is_empty() { return Err(EmailError::Empty); }
        if !value.contains("@") { return Err(EmailError::InvalidFormat); }
        Ok(Self(value))
    }

    pub fn value(&self) -> &str { &self.0 }

}
