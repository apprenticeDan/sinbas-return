//use super::user_error::UserError;

#[derive(Debug)]
pub enum EmailError { Empty, InvalidFormat, }

#[derive(Debug, Clone)]
pub struct Email(String);

impl Email {
    pub fn parse(value: String) -> Result<Self, EmailError> {
        let v = value.trim().to_string();
        if v.is_empty() { return Err(EmailError::Empty); }
        
        let parts: Vec<&str> = v.splitn(2,'@').collect();
        if parts.len() != 2 { return Err(EmailError::InvalidFormat); }

        let local = parts[0];
        let domain = parts[1];
        if local.is_empty() || !domain.contains('.') { return Err(EmailError::InvalidFormat); } 
        if domain.starts_with('.') || domain.ends_with('.') { return Err(EmailError::InvalidFormat); }

        Ok(Self(v))
    }

    pub fn value(&self) -> &str { &self.0 }

}
